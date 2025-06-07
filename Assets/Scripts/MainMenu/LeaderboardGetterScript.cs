using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Collections;
using static LeaderboardSenderScript;

public class LeaderboardGetterScript : MonoBehaviour
{

    public IEnumerator GetScores(System.Action<List<ScoreData>> callback)
    {
        string url = "https://benjoapi.zapto.org/scores";
        UnityWebRequest request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Fehler beim Laden des Scoreboards: " + request.error);
            callback(null);
        }
        else
        {
            string json = request.downloadHandler.text;
            Debug.Log("Scoreboard JSON: " + json);
            ScoreList list = JsonUtility.FromJson<ScoreList>("{\"scores\":" + json + "}");
            Debug.Log("ScoreList count: " + (list.scores != null ? list.scores.Count : 0));
            callback(list.scores);
        }
    }

    [System.Serializable]
    public class ScoreList
    {
        public List<ScoreData> scores;
    }
}

