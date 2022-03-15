using KModkit;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class TwisterScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;
    public KMBossModule BossModule;

    public GameObject Hand; // The hand of the spinner
    public KMSelectable SpinnerSelectable; // The large spinner initially shown on the module.
    public KMSelectable[] DotSelectables; // The 24 dots shown on the mat.
    public KMSelectable BodyPartPickerSel, BodyPartPickerUpSel, BodyPartPickerDownSel; // The buttons to swap players, move up, and move down.
    public KMSelectable MatSpinnerSel; // MatSpinner -> Mat Parent, Spinner Selectable. The small spinner at the bottom right of the mat screen to submit your answer.
    public KMSelectable SpinnerMatSel; // SpinnerMat -> Spinner Parent, Mat Selectable. The small mat at the bottom right of the spinner screen (during stage recovery) to return to the mat.
    public GameObject MatSpinner; // MatSpinner -> Mat Parent, Spinner Selectable.
    public GameObject SpinnerMat; // SpinnerMat -> Spinner Parent, Mat Selectable.
    public GameObject[] QueueBulbs, StageBulbs; // QueueBulbs -> The green bulbs up top to show how many spins are left in the queue. StageBulbs -> The current stage.
    public GameObject SpinnerParent, MatParent; // The screen for the spinner/stages, the screen to submit the answer on the mat.
    public Material BulbOff, QueueOn, StageOn; // Black, Green, Yellow materials.
    public GameObject[] BodyPickerParts; // The body parts that are shown on the right of the mat, for deciding which body part you are placing.
    public GameObject[] MatBodyParts; // The body parts that appear/disappear when interacting with the dots on the mat.
    public GameObject QueueBigBulb, StageBigBulb; // The large bulbs on the top left and bottom left. QueueBigBulb turns on when there are at least 64 modules in the queue (2^6). The StageBigBulb turns on when there are at least 1024 modules in the queue (2^10).

    private int _moduleId; // Module ID (for logging)
    private static int _moduleIdCounter = 1; // Module ID Counter (for logging)
    private bool _moduleSolved; // Is the module solved? If so, don't let the user interact with the module.
    private string[] ignoredModules; // For boss modules

    private float _currentAngle = 0f; // The current angle of the spinner hand. 
    private bool _isSpinning; // Is the hand spinning? If so, don't let the user interact with the spinner.
    private List<int> _spins = new List<int>(); // The spins that are calculated at the start of the bomb.
    private int _stageCount, _currentStage = 0; // The total number of stages (or non-ignored modules). The current stage.
    private int _curSolved; // The number of solved modules on the bomb. (Not the same as current stage. Responsible for queue calculation.
    private int _queueCount = 0; // The number of spins in the queue. (Or, the number of solved modules minus the current stage.
    private int THIRD; // Third digit of the SN.
    private int SIXTH; // Sixth digit of the serial number.
    private int _bodyPickerIndex = 0; // The body part for the body part picker. Left/Right, Hand/Foot.
    private int _bodyPickerPlayer = 0; // The current player on the body part picker. 1/2
    private bool _inStageRecovery; // Is the module in stage recovery? If so, spins have no animation/are instant. Small mat at the bottom right is activated so you can return to the mat.
    private bool _hasStruck; // Have you struck on submission? If so, allow the user to hold the spinner to go to stage recovery.
    private BodyParts?[][] _solutionDots = new BodyParts?[4][] { new BodyParts?[6], new BodyParts?[6], new BodyParts?[6], new BodyParts?[6] }; // The correct body part configuration.
    private BodyParts?[][] _inputDots = new BodyParts?[4][] { new BodyParts?[6], new BodyParts?[6], new BodyParts?[6], new BodyParts?[6] }; // The body part configuration that the user has inputted.
    private BodyParts _currentBodyPart; // The current body part selected by the user in the Body Part Picker.
    private bool _hasBeenHeld; // Has the small spinner been held? If so, go to stage recovery.
    private float[] xPos = { -0.0375f, -0.0125f, 0.0125f, 0.0375f }; // The X posotions of the dots.
    private float[] zPos = { 0.052f, 0.032f, 0.012f, -0.008f, -0.028f, -0.048f }; // The Z positions of the dots.
    private Coroutine _spinnerHold; // The coroutine responsible for determining how long the spinner has been held for.

    private enum BodyParts // The body parts, as enums.
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
        _moduleId = _moduleIdCounter++; // For Logging

        // START
        SpinnerSelectable.OnInteract += SpinnerAction;
        BodyPartPickerSel.OnInteract += BodyPartPickerAction;
        BodyPartPickerUpSel.OnInteract += BodyPartPickerUp;
        BodyPartPickerDownSel.OnInteract += BodyPartPickerDown;
        MatSpinnerSel.OnInteract += MatSpinnerPress;
        MatSpinnerSel.OnInteractEnded += MatSpinnerRelease;
        SpinnerMatSel.OnInteract += SpinnerMatPress;
        for (int i = 0; i < DotSelectables.Length; i++)
            DotSelectables[i].OnInteract += DotPress(DotSelectables[i], i / 6, i % 6);
        // END. Selectable press/release logic.

        var SerialNumber = BombInfo.GetSerialNumber();
        THIRD = SerialNumber[2] - '0';
        SIXTH = SerialNumber[5] - '0';
        StartCoroutine(Init()); // Read below for more.
    }

    private IEnumerator Init()
    {
        yield return null; // Wait one frame before finding stage count/calculations. Finding stage count during start has problems.
        SpinnerParent.SetActive(true); // Display the large spinner.
        MatParent.SetActive(false); // Do not display the Mat.
        SpinnerMat.SetActive(false); // Do not display the small mat on the spinner page. (It's for stage recovery)
        for (int i = 0; i < MatBodyParts.Length; i++) // Do not display any of the body parts shown on the mat.
            MatBodyParts[i].SetActive(false);

        if (ignoredModules == null) // Ignored modules.
            ignoredModules = GetComponent<KMBossModule>().GetIgnoredModules("Twister", new string[] {
                "14",
                "42",
                "501",
                "A>N<D",
                "Bamboozling Time Keeper",
                "Black Arrows",
                "Brainf---",
                "The Board Walk",
                "Busy Beaver",
                "Don't Touch Anything",
                "Doomsday Button",
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
                "OmegaDestroyer",
                "OmegaForget",
                "Organization",
                "Password Destroyer",
                "Purgatory",
                "RPS Judging",
                "Security Council",
                "Shoddy Chess",
                "Simon Forgets",
                "Simon's Stages",
                "Souvenir",
                "Tallordered Keys",
                "The Time Keeper",
                "Timing is Everything",
                "The Troll",
                "Turn the Key",
                "The Twin",
                "Twister",
                "Übermodule",
                "Ultimate Custom Night",
                "The Very Annoying Button",
                "Whiteout",
                "Zener Cards"
            });
        _stageCount = BombInfo.GetSolvableModuleNames().Count(a => !ignoredModules.Contains(a)); // Stage count is equal to all of the solveable modules on the bomb, minus ignored modules.
        for (int stageIx = 0; stageIx <= _stageCount;) //stageIx++ is moved to below, in the case that the randomly chosen bodyPart is unavailable. (Read line 160)
        {
            int randSpin = Rnd.Range(0, 16); // Randomly chooses a body part and a color. 4 colors, 4 body parts, 16 total.
            var bodyPart = (BodyParts)(randSpin / 4 + (stageIx % 2) * 4); // Decide on the body part. Even stage = player 1. Odd stage = player 2. 
            var availableDots = Enumerable.Range(0, 6).Where(ix => _solutionDots[randSpin % 4][ix] == null || _solutionDots[randSpin % 4][ix] == bodyPart).ToArray(); // Finds out the availabe amount of spaces for the determined color.
            if (availableDots.Length == 0) // If the spun color has no dots left, you aren't able to place another body part on it. Try again.
                continue;
            for (int color = 0; color < 4; color++) // For each color
                for (int dot = 0; dot < 6; dot++) // For each dot, or row
                    if (_solutionDots[color][dot] == bodyPart) // If the body part already exists on the mat, remove it.
                        _solutionDots[color][dot] = null;
            var digit = stageIx % 2 == 0 ? THIRD : SIXTH; // Determine how many dots down you have to move.
            var index = availableDots[digit % availableDots.Length]; // The determined dot, after moving down.
            _solutionDots[randSpin % 4][index] = bodyPart; // Set the body part and spin to _solutionDots.
            _spins.Add(randSpin); // Add it to the spin list.
            Debug.LogFormat("[Twister #{0}] Placement #{1} is a {2} at {3} dot #{4} for Player {5}",
                _moduleId, stageIx,
                (int)bodyPart % 4 == 0 ? "left hand" : (int)bodyPart % 4 == 1 ? "right hand" : (int)bodyPart % 4 == 2 ? "right foot" : "left foot",
                randSpin % 4 == 0 ? "green" : randSpin % 4 == 1 ? "yellow" : randSpin % 4 == 2 ? "blue" : "red",
                index + 1, (stageIx % 2) + 1);
            stageIx++; // Add to stageIx, as the randomly chosen dot was available.
        }
        Debug.LogFormat("[Twister #{0}] All stages have been completed.", _moduleId); // Necessary for LFA-side logging.
        _currentAngle = _spins[0] * 22.5f; // The angle for pointing to the first dot.
        Hand.transform.localEulerAngles = new Vector3(0f, _currentAngle, 0f); // Point the spinner to the first dot.
    }
    private KMSelectable.OnInteractHandler DotPress(KMSelectable dot, int color, int row) // The dot you pressed, the color, the row.
    {
        return delegate
        {
            if (!_moduleSolved) // If the module is solved, don't do anything.
            {
                for (int c = 0; c < 4; c++) // For each color...
                    for (int d = 0; d < 6; d++) // For each row...
                        if (_inputDots[c][d] == _currentBodyPart && (c != color || d != row)) // If the selected body part is already on a mat and you try to place it in a new position...
                            _inputDots[c][d] = null; // Remove the old body part.
                if (_inputDots[color][row] == null) // If there is no body part on the selected dot...
                {
                    MatBodyParts[(int)(_currentBodyPart)].SetActive(true); // Set it active, 
                    MatBodyParts[(int)(_currentBodyPart)].transform.localPosition = new Vector3(xPos[color], MatBodyParts[(int)(_currentBodyPart)].transform.localPosition.y, zPos[row]); // Move its position,
                    _inputDots[color][row] = _currentBodyPart; // And set it to the _inputDots array.
                }
                else
                {
                    MatBodyParts[(int)(_inputDots[color][row].Value)].SetActive(false); // If you click on a dot that already has a body part on it...
                    _inputDots[color][row] = null; // Remove it.
                }
            }
            return false;
        };
    }
    private bool BodyPartPickerAction()
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, BodyPartPickerSel.transform);
        BodyPartPickerSel.AddInteractionPunch(0.5f);
        if (!_moduleSolved) // If the module is solved, don't do anything.
        {
            _bodyPickerPlayer = (_bodyPickerPlayer + 1) % 2; // Change the player of the body part picker to the other player.
            _currentBodyPart = (BodyParts)(_bodyPickerIndex + _bodyPickerPlayer * 4); // Change the current body part to that of the other player.
            for (int bp = 0; bp < BodyPickerParts.Length; bp++) // For each of the body parts available in the body part picker...
                BodyPickerParts[bp].SetActive(bp == _bodyPickerIndex + _bodyPickerPlayer * 4); // Set the current body part active, and set the others inactive.
        }
        return false;
    }
    private bool BodyPartPickerUp()
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, BodyPartPickerUpSel.transform);
        BodyPartPickerUpSel.AddInteractionPunch(0.5f);
        if (!_moduleSolved) // If the module is solved, don't do anything.
        {
            _bodyPickerIndex = (_bodyPickerIndex + 1) % 4; // Change the body part of the body part picker to one above it in the cycle. 
            _currentBodyPart = (BodyParts)(_bodyPickerIndex + _bodyPickerPlayer * 4); // Change the current body part to that next in the cycle.
            for (int bp = 0; bp < BodyPickerParts.Length; bp++) // For each of the body parts available in the body part picker...
                BodyPickerParts[bp].SetActive(bp == _bodyPickerIndex + _bodyPickerPlayer * 4); // Set the current body part active, and set the others inactive.
        }
        return false;
    }
    private bool BodyPartPickerDown()
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, BodyPartPickerDownSel.transform);
        BodyPartPickerDownSel.AddInteractionPunch(0.5f);
        if (!_moduleSolved) // If the module is solved, don't do anything.
        {
            _bodyPickerIndex = (_bodyPickerIndex + 3) % 4;  // Change the body part of the body part picker to one below it in the cycle. 
            _currentBodyPart = (BodyParts)(_bodyPickerIndex + _bodyPickerPlayer * 4); // Change the current body part to that next in the cycle.
            for (int bp = 0; bp < BodyPickerParts.Length; bp++) // For each of the body parts available in the body part picker...
                BodyPickerParts[bp].SetActive(bp == _bodyPickerIndex + _bodyPickerPlayer * 4); // Set the current body part active, and set the others inactive.
        }
        return false;
    }
    private bool MatSpinnerPress()
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, MatSpinnerSel.transform); // Play the button release sound (quiet press) when pressing the mat spinner.
        MatSpinnerSel.AddInteractionPunch(0.5f); // Add an interaction punch when pressing.
        if (!_moduleSolved) // If the module is solved, don't do anything.
        {
            _hasBeenHeld = false; // Reset the _hasBeenHeld boolean.
            _spinnerHold = StartCoroutine(SpinnerHold()); // Start a timer.
        }
        return false;
    }
    private void MatSpinnerRelease()
    {
        if (!_moduleSolved) // If the module is solved, don't do anything.
        {
            StopCoroutine(_spinnerHold); // Stop the timer.
            if (_hasBeenHeld && _hasStruck) // If the spinner has been held for long enough, and you haven't struck,
                StageRecovery(); // Go to stage recovery.
            else
                SubmitAnswer(); // Submit the answer.
        }
    }
    private bool SpinnerAction()
    {
        if (_currentStage == _stageCount && !_isSpinning) // If all the spins have been spun, and the spin animation isn't playing...
        {
            _inStageRecovery = false; // Disable stage recovery.
            _hasStruck = false; // Turn this to false. (Already at false upon going to the mat first, but sets it back to false so you aren't able to go to stage recovery without striking.
            SpinnerParent.SetActive(false); // Set the spinner inactive.
            MatParent.SetActive(true); // Set the mat active.
            for (int bp = 0; bp < BodyPickerParts.Length; bp++) // For each body part in the body part picker...
                BodyPickerParts[bp].SetActive(bp == (int)_currentBodyPart); // Set the current one active, and the other inactive.
        }
        else if (!_isSpinning && _currentStage < _curSolved) // If the animation is not playing, and the current stage is less than the number of currently solved modules...
        {
            StartCoroutine(SpinHand()); // Play the spinning hand animation.
            for (int i = 0; i < StageBulbs.Length; i++) // For each of the stage bulbs...
                StageBulbs[i].GetComponent<MeshRenderer>().material = (_currentStage & (1 << i)) != 0 ? StageOn : BulbOff; // Set them equal to the current stage in binary.
            StageBigBulb.GetComponent<MeshRenderer>().material = (_currentStage > 1023) ? StageOn : BulbOff; // If the current stage is at least 1024, turn this bulb on. (2^10)
        }
        else if (!_isSpinning) // If the animation is not playing, and you try to spin the spinner while there are no spins in the queue,
        {
            Debug.LogFormat("[Twister #{0}] You tried to spin the spinner when there were no spins in the queue. Strike.", _moduleId);
            Module.HandleStrike(); // Strike the user.
        }
        return false;
    }

    private void SubmitAnswer()
    {
        var correctAnswer = true; // Default to true.
        for (int c = 0; c < 4; c++) // For each color,
            for (int d = 0; d < 6; d++) // For each dot,
                if (_inputDots[c][d] != _solutionDots[c][d]) // If at any point, one dot is not equal to the solution...
                    correctAnswer = false; // Set this to false.
        if (correctAnswer) // If the dot configuration is correct...
        {
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform); // Play the correct chime sound.
            Module.HandlePass(); // Solve the module.
            _moduleSolved = true; // Set this to true, so the user cannot interact with the module further.
            Debug.LogFormat("[Twister #{0}] You placed all the hands and feet in the correct positions. Module solved.", _moduleId);
        }
        else // Otherwise...
        {
            Module.HandleStrike(); // Strike the user.
            _hasStruck = true; // If this is true, allow the user to go to stage recovery.
            Debug.LogFormat("[Twister #{0}] You did not place all the hands and feet in the correct positions. Strike.", _moduleId);
        }
    }

    private bool SpinnerMatPress() // Spinner Parent, Mat selectable. To go to the mat during stage recovery.
    {
        _hasStruck = false; // Turn this to false, as you have exited stage recovery.
        SpinnerParent.SetActive(false); // Disable the spinner.
        MatParent.SetActive(true); // Enable the mat.
        for (int bp = 0; bp < BodyPickerParts.Length; bp++) // For each body part in the body part picker...
            BodyPickerParts[bp].SetActive(bp == (int)_currentBodyPart); // Set the current one active, and the other inactive.
        return false;
    }

    private void StageRecovery()
    {
        _currentStage = 0; // Set the current stage to 0.
        for (int i = 0; i < StageBulbs.Length; i++) // For each of the stage bulb lights...
            StageBulbs[i].GetComponent<MeshRenderer>().material = BulbOff; // Turn them all off. (As it’s 0 in binary)
        _currentAngle = _spins[0] * 22.5f; // Calculate the angle to the first spin.
        Hand.transform.localEulerAngles = new Vector3(0f, _currentAngle, 0f); // Set the hand to said angle.
        SpinnerParent.SetActive(true); // Enable the spinner.
        MatParent.SetActive(false); // Disable the mat.
        SpinnerMat.SetActive(true); // Spinner parent, Mat selectable. Turn this on to return to the mat. (only shown during stage recovery)
        _inStageRecovery = true; // Yes, we are now in stage recovery.
    }

    private IEnumerator SpinnerHold() 
    {
        int initialTime = (int)BombInfo.GetTime(); // Initial time.
        int currentTime; // Current time.
        int numberOfTicks = 0; // Number of timer ticks that the spinner has been held for.
        while (numberOfTicks < 2) // While the timer of timer ticks held is less than 2...
        {
            currentTime = (int)BombInfo.GetTime();
            if (currentTime != initialTime) // If the seconds digit on the timer is not equal to the last...
            {
                numberOfTicks++; // Increment the number of ticks held.
                initialTime = currentTime; // Set this to the current time. (Changes every timer tick)
            }
            yield return new WaitForSeconds(0.1f);
        }
        if (numberOfTicks >= 2) // If the spinner has been held for 2 or more timer ticks,
            _hasBeenHeld = true; // Set this to true. Upon releasing, enter stage recovery. 
    }
    private IEnumerator SpinHand() // Animation for spinning the hand.
    {
        _isSpinning = true; // Set this to true. While true, the user cannot interact with the spinner.
        _currentStage++; // Increment the current stage.
        if (!_inStageRecovery) // If the user is not in stage recovery, do this animation. If not, skip the animation, move the hand straight to the next angle.
        {
            var durationInit = 1f; // The windup time length.
            var elapsedInit = 0f;
            while (elapsedInit < durationInit)
            {
                Hand.transform.localEulerAngles = new Vector3(0f, Easing.InOutQuad(elapsedInit, _currentAngle, _currentAngle - 22.5f, durationInit), 0f);
                // Rotate the hand counterclockwise by 1 dot.
                yield return null;
                elapsedInit += Time.deltaTime;
            }
            Hand.transform.localEulerAngles = new Vector3(0f, _currentAngle - 22.5f, 0f); // Set the angle. For cleaner animation.
            var duration = 5.5f; // The spin time length.
            var elapsed = 0f;
            Audio.PlaySoundAtTransform("WheelSpin", transform);
            while (elapsed < duration)
            {
                Hand.transform.localEulerAngles = new Vector3(0f, Easing.OutQuad(elapsed, _currentAngle - 22.5f, 3600f + (_spins[_currentStage] * 22.5f), duration), 0f);
                // Spin the hand 10 full rotations, plus the angle required to point to the next dot.
                // OutQuad is used so the spinner starts spinning fast, then slows down.
                yield return null;
                elapsed += Time.deltaTime;
            }
            _currentAngle = _spins[_currentStage] * 22.5f; // Find the angle of the dot.
            Hand.transform.localEulerAngles = new Vector3(0f, _currentAngle, 0f); // Set the angle. For cleaner animation.
        }
        else
        {
            _currentAngle = _spins[_currentStage] * 22.5f; // If in stage recovery, no animation, set it immediately.
            Hand.transform.localEulerAngles = new Vector3(0f, _currentAngle, 0f); // Set the angle.
        }
        _isSpinning = false; // Spinner is no longer spinning. User is free to interact with the spinner.
    }

    private void Update() // Called every frame.
    {
        _curSolved = BombInfo.GetSolvedModuleNames().Where(x => !ignoredModules.Contains(x)).Count(); // The current number of solved modules (except ignore list)
        _queueCount = _curSolved - _currentStage; // The current number of spins in the queue, or current number of solved modules minus current stage.
        if (!_inStageRecovery) // If not in stage recovery...
        {
            for (int i = 0; i < QueueBulbs.Length; i++) // For each queue bulb...
                QueueBulbs[i].GetComponent<MeshRenderer>().material = (_queueCount & (1 << i)) != 0 ? QueueOn : BulbOff; // Set the bulbs equal to the queue count in binary.
            QueueBigBulb.GetComponent<MeshRenderer>().material = (_queueCount > 63) ? QueueOn : BulbOff; // If the queue count is at least 64, turn on the big queue bulb (2^6).
        }
        else // Otherwise
        {
            for (int i = 0; i < QueueBulbs.Length; i++)
                QueueBulbs[i].GetComponent<MeshRenderer>().material = BulbOff; // Turn off all the queue bulbs.
        }
    }

#pragma warning disable 414
    private string TwitchHelpMessage = "!{0} spin [spins the wheel] | !{0} toggle [switch between wheel and mat for stage recovery] | !{0} p1 left foot/p1lf [select a body part] | !{0} green 1 [place body part on a circle] | !{0} p1lfg1 [select a body part and place it] | !{0} submit";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        Match m;
        if (Regex.IsMatch(command, @"^\s*(go|click|run|spin|activate)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) // Command for spinning the spinner.
        {
            if (MatParent.activeSelf) // If the mat is active (meaning no spinner)...
            {
                yield return "sendtochaterror You can’t spin the wheel if it’s not visible. To enter stage recovery, use toggle."; // Invalid action.
                yield break;
            }
            yield return null;
            while (_isSpinning) // If the spinner is spinning, wait.
                yield return null;
            yield return new[] { SpinnerSelectable }; // Press the spinner.
            yield break;
        }

        if (Regex.IsMatch(command, @"^\s*toggle\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) // Toggle between the mat and the spinner.
        {
            if (_isSpinning) // If the spinner is spinning...
            {
                yield return "sendtochaterror Wait for the wheel to finish spinning first!"; // Invalid action.
                yield break;
            }
            if (!MatParent.activeSelf && !_inStageRecovery) // If you are in the spinner, and you are not in stage recovery...
            {
                yield return "sendtochaterror Solve all the modules and/or spin the wheel first!"; // Invalid action.
                yield break;
            }
            if (!_hasStruck) // If you haven't struck..
            {
                yield return "sendtochaterror You can't toggle to the spinner if you haven't submitted anything!"; // Invalid action.
                yield break;
            }

            yield return null;
            if (_inStageRecovery) // If in stage recovery...
                yield return new[] { SpinnerMatSel }; // Go to the mat.
            else
            {
                yield return MatSpinnerSel;
                yield return new WaitForSeconds(2.5f); // Hold the spinner for at least 2 seconds to go back to the spinner.
                yield return MatSpinnerSel;
            }
            yield break;
        }

        // If the command for inputting a player, body part, and dot is valid...
        if ((m = Regex.Match(command, @"^\s*(?<bp>p(?:layer)?\s*(?<p>[12])\s*(?:(?<l>l|left)|r|right)\s*(?:(?<f>f|foot)|h|hand))?\s*(?<circle>(?:(?<g>g|green)|(?<y>y|yellow)|(?<b>b|blue)|r|red)\s*(?<ix>[1-6]))?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success && (m.Groups["bp"].Success || m.Groups["circle"].Success))
        {
            if (!MatParent.activeSelf && !_inStageRecovery) // If you’re not in the mat...
            {
                yield return "sendtochaterror Solve all the modules and/or spin the wheel first!"; // Invalid action.
                yield break;
            }
            yield return null;
            if (m.Groups["bp"].Success) // If the body part command is valid...
            {
                var player = m.Groups["p"].Value[0] - '1'; // Set the player.
                var left = m.Groups["l"].Success; // set the left/right body part.
                var foot = m.Groups["f"].Success; // set the hand/foot body part.
                var bodypart = (BodyParts)((left ? foot ? BodyParts.LeftFoot1 : BodyParts.LeftHand1 : foot ? BodyParts.RightFoot1 : BodyParts.RightHand1) + 4 * player);
                if ((int)_currentBodyPart / 4 != player) // If the current player is not equal to the selected player...
                    yield return new[] { BodyPartPickerSel }; // Toggle it.
                while (_currentBodyPart != bodypart) // If the current body part is not equal to the selected body part...
                    yield return new[] { BodyPartPickerDownSel }; // Press it until it is.
            }

            if (m.Groups["circle"].Success) // If the dot command is valid...
            {
                var color = m.Groups["g"].Success ? 0 : m.Groups["y"].Success ? 1 : m.Groups["b"].Success ? 2 : 3; // Get the color.
                var ix = m.Groups["ix"].Value[0] - '1'; // Get the row.
                yield return new[] { DotSelectables[ix + color * 6] }; // Click that dot.
            }
            yield break;
        }

        if (Regex.IsMatch(command, @"^\s*submit\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) // If the submit command is valid...
        {
            if (!MatParent.activeSelf && !_inStageRecovery) // If the mat is inactive, and you are not in stage recovery...
            {
                yield return "sendtochaterror Solve all the modules and/or spin the wheel first!"; // Invalid Action.
                yield break;
            }
            yield return null;
            yield return new[] { MatSpinnerSel }; // Mat parent, spinner selectable. Press it quickly to submit.
            yield break;
        }
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        // Spin the spinner
        while (SpinnerParent.activeSelf)
        {
            if (_inStageRecovery) // While in stage recovery...
            {
                SpinnerMatSel.OnInteract(); // Press the spinner mat to go the mat.
                yield return new WaitForSeconds(0.1f); 
            }
            else if (!_isSpinning && (_queueCount != 0 || _currentStage == _stageCount)) // If the spinner is not spinning, and (the queue count is not equal to 0, or it's the final spin...)
            {
                SpinnerSelectable.OnInteract(); // Spin the spinner.
                yield return new WaitForSeconds(0.1f);
            }
            yield return true; // The module will auto solve if this isn't running, as the method has been completed. Anti-failsafe...?
        }
        // Place the things on the mat
        for (int color = 0; color < 4; color++) // For each color...
        {
            for (int row = 0; row < 6; row++) // For each row...
            {
                if (_solutionDots[color][row] == _inputDots[color][row]) // If the input is already equal to the solution...
                    continue; // Go to the next dot.
                if (_solutionDots[color][row] != null && _inputDots[color][row] == null) // If the solution is not empty, but the input is...
                {
                    var bp = _solutionDots[color][row]; // Find the body part of the solution.
                    if ((int)_currentBodyPart / 4 != (int)bp / 4) // If the current player is not equal to the player of the solution's dot...
                    {
                        BodyPartPickerSel.OnInteract(); // Toggle it.
                        yield return new WaitForSeconds(0.1f);
                    }
                    while (_currentBodyPart != bp) // While the current body part is not equal to the body part of the solution's dot...
                    {
                        BodyPartPickerDownSel.OnInteract(); // Press it until it is.
                        yield return new WaitForSeconds(0.1f);
                    }
                    DotSelectables[color * 6 + row].OnInteract(); // Press the dot.
                    yield return new WaitForSeconds(0.1f);
                    continue; // Go to the next dot.
                }
                if (_inputDots[color][row] != null) // If there is a body part on the mat that isn't correct...
                {
                    var bp = _inputDots[color][row]; // Find the body part of that dot.
                    if ((int)_currentBodyPart / 4 != (int)bp / 4) // If the current player is not equal to the player of this dot...
                    {
                        BodyPartPickerSel.OnInteract(); // Toggle it.
                        yield return new WaitForSeconds(0.1f);
                    }
                    while (_currentBodyPart != bp) // While the current body part is not equal to the body part of this dot...
                    {
                        BodyPartPickerDownSel.OnInteract(); // Press it until it is.
                        yield return new WaitForSeconds(0.1f);
                    }
                    DotSelectables[color * 6 + row].OnInteract(); // Press the dot.
                    yield return new WaitForSeconds(0.1f);
                    if (_solutionDots[color][row] == null) // If this dot of the solution is empty...
                        continue; // Go tot he next dot.
                    bp = _solutionDots[color][row]; // Find the body part of the solution dot. 
                    if ((int)_currentBodyPart / 4 != (int)bp / 4) // If the current player is not equal to the player of the solution's dot...
                    {
                        BodyPartPickerSel.OnInteract(); // Toggle it.
                        yield return new WaitForSeconds(0.1f);
                    }
                    while (_currentBodyPart != bp) // While the current body part is not equal to the body part of the solution's dot...
                    {
                        BodyPartPickerDownSel.OnInteract(); // Press it until it is.
                        yield return new WaitForSeconds(0.1f);
                    }
                    DotSelectables[color * 6 + row].OnInteract(); // Press the dot.
                    yield return new WaitForSeconds(0.1f);
                    continue; // Go to the next dot.
                }
            }
        }
        MatSpinnerSel.OnInteract(); // Press the mat spinner.
        yield return new WaitForSeconds(0.1f);
        MatSpinnerSel.OnInteractEnded(); // Release the mat spinner immediately. The release is responsible for the submission, as holding it would go to stage recovery.
        yield return new WaitForSeconds(0.1f);
    }
}
