using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rnd = UnityEngine.Random;
using KModkit;

public class TwisterScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;
    public KMBossModule BossModule;
    public GameObject Hand;
    public KMSelectable SpinnerSelectable;
    public KMSelectable[] DotSelectables;
    public KMSelectable BodyPartPickerSel, BodyPartPickerUpSel, BodyPartPickerDownSel;
    public KMSelectable MatSpinnerSel;
    public KMSelectable SpinnerMatSel;
    public GameObject MatSpinner;
    public GameObject SpinnerMat;
    public GameObject[] QueueBulbs, StageBulbs;
    public GameObject SpinnerParent, MatParent;
    public Material BulbOff, QueueOn, StageOn;
    public GameObject[] BodyPickerParts;
    public GameObject[] MatBodyParts;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;
    private string[] ignoredModules;

    private float _currentAngle = 0f;
    private bool _isSpinning;
    private List<int> _spins = new List<int>();
    private int _stageCount, _currentStage = 0;
    private int _curSolved;
    private int _queueCount = 0;
    private int THIRD;
    private int SIXTH;
    private int _bodyPickerIndex = 0;
    private int _bodyPickerPlayer = 0;
    private bool _inStageRecovery;
    private bool _hasStruck;
    private BodyParts?[][] _dots = new BodyParts?[4][] { new BodyParts?[6], new BodyParts?[6], new BodyParts?[6], new BodyParts?[6] };
    private BodyParts?[][] _answers = new BodyParts?[4][] { new BodyParts?[6], new BodyParts?[6], new BodyParts?[6], new BodyParts?[6] };
    private BodyParts _currentBodyPart;
    private bool _hasBeenHeld;
    private float[] xPos = { -0.0375f, -0.0125f, 0.0125f, 0.0375f };
    private float[] zPos = { 0.052f, 0.032f, 0.012f, -0.008f, -0.028f, -0.048f };
    private Coroutine _spinnerHold;

    private enum BodyParts
    {
        LeftHand1,
        RightHand1,
        RightFoot1,
        LeftFoot1,
        LeftHand2,
        RightHand2,
        RightFoot2,
        LeftFoot2
    }

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        SpinnerSelectable.OnInteract += SpinnerAction;
        BodyPartPickerSel.OnInteract += BodyPartPickerAction;
        BodyPartPickerUpSel.OnInteract += BodyPartPickerUp;
        BodyPartPickerDownSel.OnInteract += BodyPartPickerDown;
        MatSpinnerSel.OnInteract += MatSpinnerPress;
        MatSpinnerSel.OnInteractEnded += MatSpinnerRelease;
        SpinnerMatSel.OnInteract += SpinnerMatPress;
        for (int i = 0; i < DotSelectables.Length; i++)
            DotSelectables[i].OnInteract += DotPress(DotSelectables[i], i / 6, i % 6);
        var SerialNumber = BombInfo.GetSerialNumber();
        THIRD = SerialNumber[2] - '0';
        SIXTH = SerialNumber[5] - '0';
        StartCoroutine(Init());
    }

    private IEnumerator Init()
    {
        yield return null;
        SpinnerParent.SetActive(true);
        MatParent.SetActive(false);
        SpinnerMat.SetActive(false);
        for (int i = 0; i < MatBodyParts.Length; i++)
            MatBodyParts[i].SetActive(false);

        if (ignoredModules == null)
            ignoredModules = GetComponent<KMBossModule>().GetIgnoredModules("Twister", new string[] {
                "14",
                "42",
                "501",
                "A>N<D",
                "Black Arrows",
                "Brainf---",
                "The Board Walk",
                "Busy Beaver",
                "Don't Touch Anything",
                "Duck Konundrum",
                "Floor Lights",
                "Forget Any Color",
                "Forget Enigma",
                "Forget Everything",
                "Forget Infinity",
                "Forget It Not",
                "Forget Maze Not",
                "Forget Me Later",
                "Forget Me Not",
                "Forget Perspective",
                "Forget The Colors",
                "Forget Them All",
                "Forget This",
                "Forget Us Not",
                "Iconic",
                "Keypad Directionality",
                "Kugelblitz",
                "Multitask",
                "OmegaForget",
                "Organization",
                "Purgatory",
                "RPS Judging",
                "Security Council",
                "Shoddy Chess",
                "Simon Forgets",
                "Simon's Stages",
                "Souvenir",
                "Tallordered Keys",
                "The Troll",
                "The Twin",
                "Twister",
                "Übermodule",
                "Ultimate Custom Night",
                "The Very Annoying Button",
                "Whiteout"
            });
        _stageCount = BombInfo.GetSolvableModuleNames().Count(a => !ignoredModules.Contains(a));
        for (int stageIx = 0; stageIx <= _stageCount;)
        {
            int randSpin = Rnd.Range(0, 16);
            var bodyPart = (BodyParts)(randSpin / 4 + (stageIx % 2) * 4);
            var availableDots = Enumerable.Range(0, 6).Where(ix => _dots[randSpin % 4][ix] == null || _dots[randSpin % 4][ix] == bodyPart).ToArray();
            if (availableDots.Length == 0)
                continue;
            for (int color = 0; color < 4; color++)
                for (int dot = 0; dot < 6; dot++)
                    if (_dots[color][dot] == bodyPart)
                        _dots[color][dot] = null;
            var digit = stageIx % 2 == 0 ? THIRD : SIXTH;
            var index = availableDots[digit % availableDots.Length];
            _dots[randSpin % 4][index] = bodyPart;
            _spins.Add(randSpin);
            //Debug.LogFormat("Spin {0}: {1}", stageIx, _spins[stageIx]);
            Debug.LogFormat("[Twister #{0}] Placement #{1} is a {2} at {3} dot #{4} for Player {5}",
                _moduleId, stageIx,
                (int)bodyPart % 4 == 0 ? "left hand" : (int)bodyPart % 4 == 1 ? "right hand" : (int)bodyPart % 4 == 2 ? "right foot" : "left foot",
                randSpin % 4 == 0 ? "green" : randSpin % 4 == 1 ? "yellow" : randSpin % 4 == 2 ? "blue" : "red",
                index + 1, (stageIx % 2) + 1);
            stageIx++;
        }
        _currentAngle = _spins[0] * 22.5f;
        Hand.transform.localEulerAngles = new Vector3(0f, _currentAngle, 0f);
    }

    private KMSelectable.OnInteractHandler DotPress(KMSelectable dot, int color, int row)
    {
        return delegate
        {
            //Debug.LogFormat("{0} {1}", color, row);
            for (int c = 0; c < 4; c++)
                for (int d = 0; d < 6; d++)
                    if (_answers[c][d] == _currentBodyPart && (c != color || d != row))
                        _answers[c][d] = null;
            if (_answers[color][row] == null)
            {
                MatBodyParts[(int)(_currentBodyPart)].SetActive(true);
                MatBodyParts[(int)(_currentBodyPart)].transform.localPosition = new Vector3(xPos[color], MatBodyParts[(int)(_currentBodyPart)].transform.localPosition.y, zPos[row]);
                _answers[color][row] = _currentBodyPart;
            }
            else
            {
                MatBodyParts[(int)(_answers[color][row].Value)].SetActive(false);
                _answers[color][row] = null;
            }
            return false;
        };
    }

    private bool BodyPartPickerAction()
    {
        _bodyPickerPlayer = (_bodyPickerPlayer + 1) % 2;
        _currentBodyPart = (BodyParts)(_bodyPickerIndex + _bodyPickerPlayer * 4);
        for (int bp = 0; bp < BodyPickerParts.Length; bp++)
            BodyPickerParts[bp].SetActive(bp == _bodyPickerIndex + _bodyPickerPlayer * 4);
        return false;
    }
    private bool BodyPartPickerUp()
    {
        _bodyPickerIndex = (_bodyPickerIndex + 1) % 4;
        _currentBodyPart = (BodyParts)(_bodyPickerIndex + _bodyPickerPlayer * 4);
        for (int bp = 0; bp < BodyPickerParts.Length; bp++)
            BodyPickerParts[bp].SetActive(bp == _bodyPickerIndex + _bodyPickerPlayer * 4);
        return false;
    }
    private bool BodyPartPickerDown()
    {
        _bodyPickerIndex = (_bodyPickerIndex + 3) % 4;
        _currentBodyPart = (BodyParts)(_bodyPickerIndex + _bodyPickerPlayer * 4);
        for (int bp = 0; bp < BodyPickerParts.Length; bp++)
            BodyPickerParts[bp].SetActive(bp == _bodyPickerIndex + _bodyPickerPlayer * 4);
        return false;
    }

    private bool MatSpinnerPress()
    {
        if (!_moduleSolved)
        {
            _hasBeenHeld = false;
            _spinnerHold = StartCoroutine(SpinnerHold());
        }
        return false;
    }
    private void MatSpinnerRelease()
    {
        if (!_moduleSolved)
        {
            StopCoroutine(_spinnerHold);
            if (_hasBeenHeld && _hasStruck)
                StageRecovery();
            else
                SubmitAnswer();
        }
    }

    private bool SpinnerAction()
    {
        if (_currentStage == _stageCount && !_isSpinning)
        {
            _hasStruck = false;
            SpinnerParent.SetActive(false);
            MatParent.SetActive(true);
            //MatSpinner.SetActive(false);
            for (int bp = 0; bp < BodyPickerParts.Length; bp++)
                BodyPickerParts[bp].SetActive(bp == _bodyPickerIndex);
        }
        else if (!_isSpinning && _currentStage < _curSolved)
        {
            StartCoroutine(SpinHand());
            for (int i = 0; i < StageBulbs.Length; i++)
                StageBulbs[i].GetComponent<MeshRenderer>().material = (_currentStage & (1 << i)) != 0 ? StageOn : BulbOff;
        }
        else if (!_isSpinning)
        {
            Debug.LogFormat("[Twister #{0}] You tried to spin the spinner when there were no spins in the queue. Strike.", _moduleId);
            Module.HandleStrike();
        }
        return false;
    }

    private void SubmitAnswer()
    {
        var correctAnswer = true;
        for (int c = 0; c < 4; c++)
            for (int d = 0; d < 6; d++)
                if (_answers[c][d] != _dots[c][d])
                    correctAnswer = false;
        if (correctAnswer)
        {
            Module.HandlePass();
            _moduleSolved = true;
            Debug.LogFormat("[Twister #{0}] You placed all the hands and feet in the correct positions. Module solved.", _moduleId);
        }
        else
        {
            Module.HandleStrike();
            _hasStruck = true;
            Debug.LogFormat("[Twister #{0}] You did not place all the hands and feet in the correct positions. Strike.", _moduleId);
        }
    }

    private bool SpinnerMatPress()
    {
        _hasStruck = false;
        SpinnerParent.SetActive(false);
        MatParent.SetActive(true);
        for (int bp = 0; bp < BodyPickerParts.Length; bp++)
            BodyPickerParts[bp].SetActive(bp == _bodyPickerIndex);
        return false;
    }

    private void StageRecovery()
    {
        _currentStage = 0;
        for (int i = 0; i < StageBulbs.Length; i++)
            StageBulbs[i].GetComponent<MeshRenderer>().material = BulbOff;
        _currentAngle = _spins[0] * 22.5f;
        Hand.transform.localEulerAngles = new Vector3(0f, _currentAngle, 0f);
        SpinnerParent.SetActive(true);
        MatParent.SetActive(false);
        SpinnerMat.SetActive(true);
        _inStageRecovery = true;
    }

    private IEnumerator SpinnerHold()
    {
        int initialTime = (int)BombInfo.GetTime();
        int currentTime;
        int numberOfTicks = 0;
        while (numberOfTicks < 2)
        {
            currentTime = (int)BombInfo.GetTime();
            if (currentTime != initialTime)
            {
                numberOfTicks++;
                initialTime = currentTime;
            }
            yield return new WaitForSeconds(0.1f);
        }
        if (numberOfTicks >= 2)
            _hasBeenHeld = true;
    }
    private IEnumerator SpinHand()
    {
        _isSpinning = true;
        _currentStage++;
        if (!_inStageRecovery)
        {
            var durationInit = 1f;
            var elapsedInit = 0f;
            while (elapsedInit < durationInit)
            {
                Hand.transform.localEulerAngles = new Vector3(0f, Easing.InOutQuad(elapsedInit, _currentAngle, _currentAngle - 22.5f, durationInit), 0f);
                yield return null;
                elapsedInit += Time.deltaTime;
            }
            Hand.transform.localEulerAngles = new Vector3(0f, _currentAngle - 22.5f, 0f);
            var duration = 5.5f;
            var elapsed = 0f;
            Audio.PlaySoundAtTransform("WheelSpin", transform);
            while (elapsed < duration)
            {
                Hand.transform.localEulerAngles = new Vector3(0f, Easing.OutQuad(elapsed, _currentAngle - 22.5f, 3600f + (_spins[_currentStage] * 22.5f), duration), 0f);
                yield return null;
                elapsed += Time.deltaTime;
            }
            _currentAngle = _spins[_currentStage] * 22.5f;
            Hand.transform.localEulerAngles = new Vector3(0f, _currentAngle, 0f);
        }
        else
        {
            _currentAngle = _spins[_currentStage] * 22.5f;
            Hand.transform.localEulerAngles = new Vector3(0f, _currentAngle, 0f);
        }
        _isSpinning = false;
    }

    private void Update()
    {
        _curSolved = BombInfo.GetSolvedModuleNames().Where(x => !ignoredModules.Contains(x)).Count();
        _queueCount = _curSolved - _currentStage;
        if (!_inStageRecovery)
        {
            for (int i = 0; i < QueueBulbs.Length; i++)
                QueueBulbs[i].GetComponent<MeshRenderer>().material = (_queueCount & (1 << i)) != 0 ? QueueOn : BulbOff;
        }
        else
        {
            for (int i = 0; i < QueueBulbs.Length; i++)
                QueueBulbs[i].GetComponent<MeshRenderer>().material = BulbOff;
        }
    }
}
