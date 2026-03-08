/* 
    ------------------- Code Monkey -------------------

    Thank you for downloading this package
    I hope you find it useful in your projects
    If you have any questions let me know
    Cheers!

               unitycodemonkey.com
    --------------------------------------------------
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UI_Blocker : MonoBehaviour {

    private static UI_Blocker instance;

    private void Awake() {
        // Keep scene-local behavior: whichever scene instance wakes last becomes active.
        instance = this;
        
        GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        GetComponent<RectTransform>().sizeDelta = Vector2.zero;

        Hide_Static();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public static void Show_Static() {
        UI_Blocker target = ResolveInstance();
        if (target == null)
            return;

        target.gameObject.SetActive(true);
        target.transform.SetAsLastSibling();
    }

    public static void Hide_Static() {
        UI_Blocker target = ResolveInstance();
        if (target == null)
            return;

        target.gameObject.SetActive(false);
    }

    private static UI_Blocker ResolveInstance()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        if (instance != null)
        {
            if (instance.gameObject.scene.IsValid() && instance.gameObject.scene == activeScene)
                return instance;
        }

        UI_Blocker[] all = Resources.FindObjectsOfTypeAll<UI_Blocker>();

        // 1) Prefer an instance that belongs to the active scene.
        for (int i = 0; i < all.Length; i++)
        {
            UI_Blocker candidate = all[i];
            if (candidate == null)
                continue;

            if (!candidate.gameObject.scene.IsValid())
                continue;

            if (candidate.gameObject.scene == activeScene)
            {
                instance = candidate;
                break;
            }
        }

        if (instance != null)
            return instance;

        // 2) Fallback to any valid scene object (including DontDestroyOnLoad).
        for (int i = 0; i < all.Length; i++)
        {
            UI_Blocker candidate = all[i];
            if (candidate != null && candidate.gameObject.scene.IsValid())
            {
                instance = candidate;
                break;
            }
        }

        return instance;
    }

}
