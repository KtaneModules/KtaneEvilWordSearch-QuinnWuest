using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rnd = UnityEngine.Random;
using KModkit;

public class EvilWordSearchScript : MonoBehaviour
{
    public KMBombInfo BombInfo;
    public KMBombModule Module;
    public KMAudio Audio;

    public Texture[] Images;
    public KMSelectable[] ScreenSels;
    public TextMesh[] ScreenTexts;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;

    private int[] _letterGrid = new int[72];
    private bool[] _selectedCells = new bool[72];
    private string _currentInput = "";
    private Color32[] _textColors = new Color32[] { new Color32(0, 190, 255, 255), new Color32(0, 120, 255, 255) };

    private int _currentScreen;
    private int _prevClickedCell = -1;
    private int _prevClickedScreen = -1;

    private static readonly string[] _wordList = new string[] { "ABANDONED", "ASSISTANT", "ATHLETICS", "AVAILABLE", "BEAUTIFUL", "BILLBOARD", "BROADCAST", "BUILDINGS", "CANDIDATE", "CHALLENGE", "COMMUNITY", "CRICKETER", "DANGEROUS", "DEVELOPED", "DIFFERENT", "DOCTORATE", "EDUCATION", "EQUIPMENT", "ESTIMATES", "EXECUTIVE", "FINANCIAL", "FOLLOWING", "FREQUENCY", "FURNITURE", "GALLERIES", "GEOGRAPHY", "GRADUATED", "GUITARIST", "HAPPINESS", "HISTORIAN", "HOUSEHOLD", "HURRICANE", "ILLUSIONS", "IMPORTANT", "INCLUDING", "IRREGULAR", "JACKKNIFE", "JELLYFISH", "JOBHOLDER", "JUDGEMENT", "KEYBOARDS", "KIDNAPPED", "KNOWLEDGE", "KOOKINESS", "LANDSCAPE", "LEGENDARY", "LIMESTONE", "LOCATIONS", "MAGAZINES", "MEANWHILE", "MONASTERY", "MUNICIPAL", "NATURALLY", "NEWSPAPER", "NIGHTCLUB", "NOMINATED", "ONSLAUGHT", "OPERATION", "ORCHESTRA", "OWNERSHIP", "PERMANENT", "POTENTIAL", "PRESIDENT", "PURCHASED", "QUADRATIC", "QUARRYING", "QUEBECOIS", "QUINIDINE", "RATIONALE", "REFERENCE", "RIGHTEOUS", "ROTATIONS", "SEPTEMBER", "SITUATION", "SOMETIMES", "STRUCTURE", "TECHNICAL", "THEREFORE", "TRANSPORT", "TYPICALLY", "UNIVERSAL", "UPGRADING", "USURPINGS", "UTILITIES", "VARIABLES", "VEGETABLE", "VIOLINIST", "VOLUNTEER", "WATERFALL", "WHICHEVER", "WORLDWIDE", "WRESTLING", "XANTHONES", "XENOGLYPH", "XEROPHYTE", "XYLOPHONE", "YARDSTICK", "YESTERDAY", "YOUNGSTER", "YUCKINESS", "ZAPATEADO", "ZEBRAFISH", "ZIGZAGGED", "ZOOGRAPHY" };
    private string _solution;
    private float _elapsedTime;
    private Coroutine _holdTimer;
    private bool _doSubmit;
    private bool _canInteract = true;
    private List<int> _selectionOrder = new List<int>();
    private List<int> _inputOrder = new List<int>();

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        for (int btn = 0; btn < ScreenSels.Length; btn++)
        {
            ScreenSels[btn].OnInteract += ScreenPress(btn);
            ScreenSels[btn].OnInteractEnded += ScreenRelease(btn);
        }
        GenerateAnswer();
        StartCoroutine(CycleScreen());
    }

    private KMSelectable.OnInteractHandler ScreenPress(int btn)
    {
        return delegate ()
        {
            if (_moduleSolved || !_canInteract)
                return false;
            if (_prevClickedCell == -1 || _prevClickedScreen == -1)
            {
                _selectedCells[_currentScreen * 36 + btn] = true;
                ScreenTexts[btn].color = new Color32(255, 255, 255, 255);
                _currentInput += "ABCDEFGHIJKLMNOPQRSTUVWXYZ"[_letterGrid[_currentScreen * 36 + btn]];
                _prevClickedCell = btn;
                _prevClickedScreen = _currentScreen;
                Debug.LogFormat("[Evil Word Search #{0}] Pressed starting cell: {1} ({2}) on screen {3}.", _moduleId, GetCoord(btn), "ABCDEFGHIJKLMNOPQRSTUVWXYZ"[_letterGrid[_currentScreen * 36 + btn]], "AB"[_currentScreen]);
                Debug.LogFormat("[Evil Word Search #{0}] Input is now {1}", _moduleId, _currentInput);
                Audio.PlaySoundAtTransform("On1", transform);
                return false;
            }
            if (_selectedCells[_currentScreen * 36 + btn])
            {
                _doSubmit = true;
                _holdTimer = StartCoroutine(HoldTime());
                return false;
            }
            if (_prevClickedScreen == _currentScreen)
            {
                Debug.LogFormat("[Evil Word Search #{0}] Pressed a cell on screen {1}, which was the screen of the previous selection. Strike.", _moduleId, "AB"[_currentScreen]);
                StartCoroutine(Strike());
                return false;
            }
            if (!GetAdjacents(btn).ToArray().Contains(_prevClickedCell))
            {
                Debug.LogFormat("[Evil Word Search #{0}] Pressed {1}, a cell that is not adjacent to {2}, the previously selected cell. Strike.", _moduleId, GetCoord(btn), GetCoord(_prevClickedCell));
                StartCoroutine(Strike());
                return false;
            }
            Audio.PlaySoundAtTransform("On1", transform);
            Debug.LogFormat("[Evil Word Search #{0}] Pressed cell: {1} ({2}) on screen {3}.", _moduleId, GetCoord(btn), "ABCDEFGHIJKLMNOPQRSTUVWXYZ"[_letterGrid[_currentScreen * 36 + btn]], "AB"[_currentScreen]);
            _selectedCells[_currentScreen * 36 + btn] = true;
            ScreenTexts[btn].color = new Color32(255, 255, 255, 255);
            _currentInput += "ABCDEFGHIJKLMNOPQRSTUVWXYZ"[_letterGrid[_currentScreen * 36 + btn]];
            _prevClickedCell = btn;
            _prevClickedScreen = _currentScreen;
            _inputOrder.Add(_currentScreen * 36 + btn);
            Debug.LogFormat("[Evil Word Search #{0}] Input is now {1}", _moduleId, _currentInput);
            return false;
        };
    }

    private Action ScreenRelease(int btn)
    {
        return delegate ()
        {
            if (!_doSubmit)
                return;
            _doSubmit = false;
            if (_holdTimer != null)
                StopCoroutine(_holdTimer);
            if (_elapsedTime < 1f)
            {
                if (_currentInput == _solution)
                {
                    Debug.LogFormat("[Evil Word Search #{0}] Correctly submitted {1}. Module solved.", _moduleId, _currentInput);
                    StartCoroutine(Solve());
                }
                else
                {
                    Debug.LogFormat("[Evil Word Search #{0}] Incorrectly submitted {1}. Strike.", _moduleId, _currentInput);
                    StartCoroutine(Strike());
                }
            }
            else
            {
                Debug.LogFormat("[Evil Word Search #{0}] Resetting current input.", _moduleId);
                Audio.PlaySoundAtTransform("Off1", transform);
                Reset();
            }
        };
    }

    private IEnumerator Strike()
    {
        _canInteract = false;
        Audio.PlaySoundAtTransform("Off2", transform);
        yield return new WaitForSeconds(0.5f);
        Module.HandleStrike();
        Reset();
        _canInteract = true;
    }

    private IEnumerator Solve()
    {
        _canInteract = false;
        Audio.PlaySoundAtTransform("On2", transform);
        yield return new WaitForSeconds(0.5f);
        Module.HandlePass();
        _moduleSolved = true;
    }

    private void Reset()
    {
        for (int i = 0; i < 36; i++)
            ScreenTexts[i].color = _textColors[_currentScreen];
        _prevClickedCell = -1;
        _prevClickedScreen = -1;
        _currentInput = "";
        _selectedCells = new bool[72];
        _inputOrder = new List<int>();
    }

    private IEnumerator HoldTime()
    {
        _elapsedTime = 0f;
        while (true)
        {
            yield return null;
            _elapsedTime += Time.deltaTime;
        }
    }

    private IEnumerator CycleScreen()
    {
        while (!_moduleSolved)
        {
            _currentScreen = (_currentScreen + 1) % 2;
            for (int i = 0; i < ScreenTexts.Length; i++)
            {
                ScreenTexts[i].text = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"[_letterGrid[_currentScreen * 36 + i]].ToString();
                ScreenTexts[i].color = _selectedCells[_currentScreen * 36 + i] ? new Color32(255, 255, 255, 255) : _textColors[_currentScreen];
            }
            yield return new WaitForSeconds(1.5f);
        }
    }

    private void GenerateAnswer()
    {
        _solution = _wordList[(BombInfo.GetSerialNumberLetters().ToArray()[0] - 'A') * 4 + Rnd.Range(0, 4)];
        Debug.LogFormat("[Evil Word Search #{0}] Chosen word: {1}", _moduleId, _solution);

        retry:
        // Fill the grid in with random letters, then place the word in.
        for (int i = 0; i < 72; i++)
            _letterGrid[i] = Rnd.Range(0, 26);
        var visited = new bool[72];
        var curPos = Rnd.Range(0, 36);
        var curGrid = Rnd.Range(0, 2);
        var list = new List<string>();
        _selectionOrder = new List<int>();
        for (int i = 0; i < _solution.Length; i++)
        {
            _letterGrid[curGrid * 36 + curPos] = _solution[i] - 'A';
            list.Add(GetCoord(curPos, curGrid));
            _selectionOrder.Add(curGrid * 36 + curPos);
            visited[curGrid * 36 + curPos] = true;
            curPos = GetAdjacents(curPos).PickRandom();
            curGrid = (curGrid + 1) % 2;
        }
        if (_selectionOrder.Distinct().Count() != _selectionOrder.Count())
            goto retry;

        // Check to see if the word is in the grid multiple times.
        List<int> q;
        List<int> qIxs;
        var firstHit = false;
        for (int i = 0; i < _wordList.Length; i++)
        {
            q = new List<int>();
            qIxs = new List<int>();
            for (int j = 0; j < _letterGrid.Length; j++)
            {
                if (_letterGrid[j] == _solution[0])
                {
                    q.Add(j);
                    qIxs.Add(0);
                }
            }
            while (q.Count > 0)
            {
                int curItem = q[0];
                int curIx = qIxs[0];
                q.RemoveAt(0);
                qIxs.RemoveAt(0);
                if (curIx == _wordList[i].Length - 1 && _solution != _wordList[i])
                    goto retry;
                else if (curIx == _wordList[i].Length - 1 && _solution == _wordList[i])
                {
                    if (!firstHit)
                        firstHit = true;
                    else
                        goto retry;
                }
                else
                {
                    var pos = CheckPositions(curItem, _wordList[i][curIx + 1]);
                    for (int j = 0; j < pos.Count; j++)
                    {
                        q.Insert(0, pos[j]);
                        qIxs.Insert(0, curIx + 1);
                    }
                }
            }
        }

        var g = _letterGrid.Select(i => "ABCDEFGHIJKLMNOPQRSTUVWXYZ"[i].ToString()).ToArray().Join("");
        Debug.LogFormat("[Evil Word Search #{0}] Grid A:", _moduleId);
        for (int i = 0; i < 6; i++)
        {
            string str = "";
            for (int j = 0; j < 6; j++)
            {
                str += g[i * 6 + j];
                if (j != 5)
                    str += " ";
            }
            Debug.LogFormat("[Evil Word Search #{0}] {1}", _moduleId, str);
        }
        Debug.LogFormat("[Evil Word Search #{0}] Grid B:", _moduleId);
        for (int i = 6; i < 12; i++)
        {
            string str = "";
            for (int j = 0; j < 6; j++)
            {
                str += g[i * 6 + j];
                if (j != 5)
                    str += " ";
            }
            Debug.LogFormat("[Evil Word Search #{0}] {1}", _moduleId, str);
        }
        Debug.LogFormat("[Evil Word Search #{0}] Solution path: {1}.", _moduleId, list.Join(", "));
    }

    private List<int> GetAdjacents(int num)
    {
        var list = new List<int>();
        var row = num / 6;
        var col = num % 6;
        list.Add(row * 6 + (col + 1) % 6);
        list.Add(row * 6 + (col + 5) % 6);
        list.Add((row + 1) % 6 * 6 + col);
        list.Add((row + 5) % 6 * 6 + col);
        list.Add((row + 1) % 6 * 6 + (col + 1) % 6);
        list.Add((row + 1) % 6 * 6 + (col + 5) % 6);
        list.Add((row + 5) % 6 * 6 + (col + 1) % 6);
        list.Add((row + 5) % 6 * 6 + (col + 5) % 6);
        return list;
    }

    private List<int> CheckPositions(int curPos, char letter)
    {
        var pos = new List<int>();
        var adj = GetAdjacents(curPos);
        for (int i = 0; i < 8; i++)
            if (_letterGrid[adj[i]] == letter)
                pos.Add(adj[i]);
        return pos;
    }

    private string GetCoord(int num, int grid = 2)
    {
        return "ABCDEF"[num % 6].ToString() + "123456"[num / 6].ToString() + (grid == 2 ? "" : "AB"[grid].ToString());
    }

#pragma warning disable 414
    private string TwitchHelpMessage = "!{0} submit A1A B2B C3A [Submit the following: A1 on Grid A, B2 on Grid B, C3 on Grid A...] | 'submit' is optional.";
#pragma warning restore 414

    private static readonly string[] _tpCoords = new string[] { "A1A", "B1A", "C1A", "D1A", "E1A", "F1A", "A2A", "B2A", "C2A", "D2A", "E2A", "F2A", "A3A", "B3A", "C3A", "D3A", "E3A", "F3A", "A4A", "B4A", "C4A", "D4A", "E4A", "F4A", "A5A", "B5A", "C5A", "D5A", "E5A", "F5A", "A6A", "B6A", "C6A", "D6A", "E6A", "F6A", "A1B", "B1B", "C1B", "D1B", "E1B", "F1B", "A2B", "B2B", "C2B", "D2B", "E2B", "F2B", "A3B", "B3B", "C3B", "D3B", "E3B", "F3B", "A4B", "B4B", "C4B", "D4B", "E4B", "F4B", "A5B", "B5B", "C5B", "D5B", "E5B", "F5B", "A6B", "B6B", "C6B", "D6B", "E6B", "F6B" };

    IEnumerator ProcessTwitchCommand(string command)
    {
        var parameters = command.ToUpperInvariant().Split(' ');
        var list = new List<int>();
        for (int i = parameters[0] == "SUBMIT" ? 1 : 0; i < parameters.Length; i++)
        {
            int ix = Array.IndexOf(_tpCoords, parameters[i]);
            if (ix == -1)
            {
                yield return "sendtochaterror " + parameters[i] + " is not a valid coordiante! Command ignored.";
                yield break;
            }
            list.Add(ix);
        }
        yield return null;
        yield return "strike";
        yield return "solve";
        if (_inputOrder.Count != 0)
        {
            while (_inputOrder[0] / 36 != _currentScreen)
                yield return null;
            ScreenSels[_inputOrder[0] % 36].OnInteract();
            while (_elapsedTime <= 1f)
                yield return null;
            ScreenSels[_inputOrder[0] % 36].OnInteractEnded();
            yield return new WaitForSeconds(0.1f);
        }
        for (int i = 0; i < list.Count; i++)
        {
            while (list[i] / 36 != _currentScreen)
                yield return null;
            ScreenSels[list[i] % 36].OnInteract();
            yield return new WaitForSeconds(0.1f);
            ScreenSels[list[i] % 36].OnInteractEnded();
            yield return new WaitForSeconds(0.1f);
        }
        while (list[0] / 36 != _currentScreen)
            yield return null;
        ScreenSels[list[0] % 36].OnInteract();
        yield return new WaitForSeconds(0.1f);
        ScreenSels[list[0] % 36].OnInteractEnded();
        yield break;
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        int start = 0;
        for (int i = 0; i < _inputOrder.Count; i++)
        {
            if (_inputOrder[i] == _selectionOrder[i])
            {
                start = i;
                continue;
            }
            else
            {
                start = 0;
                while (_inputOrder[i] / 36 != _currentScreen)
                    yield return null;
                ScreenSels[_inputOrder[i] % 36].OnInteract();
                while (_elapsedTime <= 1f)
                    yield return null;
                ScreenSels[_inputOrder[i] % 36].OnInteractEnded();
                yield return new WaitForSeconds(0.1f);
            }
        }
        for (int i = start; i < _selectionOrder.Count; i++)
        {
            while (_selectionOrder[i] / 36 != _currentScreen)
                yield return null;
            Debug.Log(_selectionOrder[i]);
            ScreenSels[_selectionOrder[i] % 36].OnInteract();
            yield return new WaitForSeconds(0.1f);
            ScreenSels[_selectionOrder[i] % 36].OnInteractEnded();
            yield return new WaitForSeconds(0.1f);
        }
        while (_selectionOrder[0] / 36 != _currentScreen)
            yield return null;
        ScreenSels[_selectionOrder[0] % 36].OnInteract();
        yield return new WaitForSeconds(0.1f);
        ScreenSels[_selectionOrder[0] % 36].OnInteractEnded();
        while (!_moduleSolved)
            yield return true;
    }
}
