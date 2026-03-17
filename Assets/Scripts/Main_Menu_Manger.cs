using UnityEngine;
using UnityEngine.SceneManagement;

public class Main_Menu_Manger : MonoBehaviour
{
  
    public void loadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);

    }

    public void quitGame()
    {
        Application.Quit();
    }

}
