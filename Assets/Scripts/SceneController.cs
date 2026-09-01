using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    public void LoadSceneByName(string Scenename)
    {
        SceneManager.LoadScene(Scenename);
    }
}
