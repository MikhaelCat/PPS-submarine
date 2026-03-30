using UnityEngine;

public class AppQuit : MonoBehaviour
{
    public void QuitProgram()
    {
        Debug.Log("Программа закрывается..."); // Пишет в консоль для проверки

        // Эта команда закроет собранную игру (.exe, .apk и т.д.)
        Application.Quit();

        // А этот кусок кода остановит игру прямо в редакторе Unity, чтобы ты видел, что кнопка работает
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}