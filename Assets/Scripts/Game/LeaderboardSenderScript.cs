using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class LeaderboardSenderScript : MonoBehaviour
{
    public IEnumerator SendScore(string username, int score)
    {
        string url = "https://benjoapi.zapto.org/score";
        var data = new ScoreData { username = username, score = score };
        string json = JsonUtility.ToJson(data);

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
            Debug.LogError("Fehler beim Senden des Scores: " + request.error);
        else
            Debug.Log("Score erfolgreich gesendet");
    }

    [System.Serializable]
    public class ScoreData
    {
        public string username;
        public int score;
    }
}
