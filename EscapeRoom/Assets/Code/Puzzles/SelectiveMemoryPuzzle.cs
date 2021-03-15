﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using UnityEngine.Events;

public class SelectiveMemoryPuzzle : Puzzlebase
{
    [System.Serializable]
    public struct Variant
    {
        public Sprite[] Images;
        public QuestionAnswerSet[] SeqeunceQuestions;        
    }

    [System.Serializable]
    public struct QuestionAnswerSet
    {
        public string Question;
        public string[] AnswerOptions;
        public string CorrectAnswer;
    }

    public Variant[] PuzzleVariants;

    public Text QuestionText, AnswerText;
    public Image ImageBoard;
    public Transform AnswerBoard;
    public SelectionOption AnswerButtonPrefab;
    public UnityEvent EndPuzzleEvent;

    Variant SelectedVariant;
    QuestionAnswerSet MySelectedQuestion;

    public override void StartPuzzle()//only call for player
    {
        Debug.Log("Host start puzzle");
        int SelectedVairiant = Random.Range(0, PuzzleVariants.Length);
        GetComponent<PhotonView>().RPC("InitializePuzzle", RpcTarget.AllViaServer, SelectedVairiant);
    }

    [PunRPC]
    public void InitializePuzzle(int Variant)//get all clients to run the selected puzzle from the current player
    {
        Debug.Log("Client init puzzle");
        Answers.Clear();
        PlayersAnswered = 0;

        //Same for all players:
        SelectedVariant = PuzzleVariants[Variant];
        //Different question for all players:
        MySelectedQuestion = SelectedVariant.SeqeunceQuestions[Random.Range(0, SelectedVariant.SeqeunceQuestions.Length)];

        QuestionText.text = MySelectedQuestion.Question;

        //Start sequence
        StartCoroutine(RunSequence());
    }

    IEnumerator RunSequence()
    {
        Debug.Log("Client run sequence");
        //Remove old quesiton cards
        List<Transform> childs = new List<Transform>();
        childs.AddRange(AnswerBoard.GetComponentsInChildren<Transform>());
        childs.Remove(AnswerBoard);
        while (childs.Count > 0) { Destroy(childs[0].gameObject);childs.RemoveAt(0); }

        AnswerText.enabled = false;

        //Help with reading the question before the sequence
        yield return new WaitForSeconds(1.5f);

        AnswerBoard.gameObject.SetActive(false);
        ImageBoard.enabled = true;

        //Play sequence
        float TimeBetweenImages = 1f;
        WaitForSeconds WSF = new WaitForSeconds(TimeBetweenImages);

        foreach (Sprite s in SelectedVariant.Images)
        {
            ImageBoard.sprite = s;
            yield return WSF;
        }

        ImageBoard.enabled = false;

        //Display answer board
        AnswerBoard.gameObject.SetActive(true);
        foreach (string s in MySelectedQuestion.AnswerOptions)
        {
            SelectionOption newButton = Instantiate(AnswerButtonPrefab, AnswerBoard);
            newButton.Answertext.text = s;
            newButton.MyPuzzle = this;
        }
    }

    public void CheckAnswer(string SelectedAnswer)
    {
        AnswerBoard.gameObject.SetActive(false);
        AnswerText.text = "Waiting for other players!";
        AnswerText.enabled = true;

        bool Correct = false;
        if (SelectedAnswer == MySelectedQuestion.CorrectAnswer)
        {
            Correct = true;
            Debug.Log("Correct");
        }
        else
        {
            Debug.Log("Incorrect, you entered: " + SelectedAnswer + ", the answer is: " + MySelectedQuestion.CorrectAnswer);
        }

        GetComponent<PhotonView>().RPC("PlayerLockIn", RpcTarget.AllViaServer,PhotonNetwork.LocalPlayer.NickName,Correct);
    }

    int PlayersAnswered = 0;
    Dictionary<string, bool> Answers = new Dictionary<string, bool>();

    [PunRPC]
    public void PlayerLockIn(string Nickname, bool Correct)
    {
        PlayersAnswered++;
        Answers.Add(Nickname, Correct);

        //Just run by the master client
        if (PhotonNetwork.MasterClient != PhotonNetwork.LocalPlayer) { return; }

        //All players have answered, show the result
        if(PlayersAnswered>= PhotonNetwork.PlayerList.Length)
        {
            GetComponent<PhotonView>().RPC("ShowResult", RpcTarget.AllViaServer);
        }
    }

    [PunRPC]
    public void ShowResult()
    {
        string Result = "";
        int c = 0;
        foreach (KeyValuePair<string, bool> kvp in Answers)
        {
            if (!kvp.Value)
            {
                if (c == 0)
                {
                    Result += kvp.Key;
                }
                else
                {
                    Result += ", " + kvp.Key;
                }
            }
            c++;
        }
        if (Result.Length > 0)
        {
            Result += " answered incorrectly! Restarting the puzzle!";

            //Host restart the puzzle
            if (PhotonNetwork.IsMasterClient)
            {
                Invoke("StartPuzzle", 3);
            }
        }
        else
        {
            Result += "Everyone got it correct! Moving to the next puzzle.";

            Invoke("NextPuzzle", 3);
        }
        AnswerText.text = Result;
    }

    private void NextPuzzle()
    {
        EndPuzzleEvent.Invoke();
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("Start next puzzle as host");
            FindObjectOfType<PuzzleManager>().HostStartNextPuzzle();
        }
    }

}
