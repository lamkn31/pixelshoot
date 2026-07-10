using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Wayfu.Lamkn
{
[System.Serializable]
public class RebuildLayouts
{
    [SerializeField] private RectTransform[] rectToRebuilds;
    
    public void Rebuild()
    {
        RebuildAllRect();
        if(GameManager.Instance)
            GameManager.Instance.StartCoroutine(IERebuild());
    }
    public void Rebuild(System.Action onComplete)
    {
        RebuildAllRect();
        if(GameManager.Instance)
            GameManager.Instance.StartCoroutine(IERebuild(onComplete));
    }
    private IEnumerator IERebuild()
    {
        yield return new WaitForEndOfFrame();
        RebuildAllRect();
    }
    private IEnumerator IERebuild(System.Action onComplete)
    {
        yield return new WaitForEndOfFrame();
        RebuildAllRect();
        onComplete?.Invoke();
    }
    public void RebuildAllRect()
    {
        foreach (var item in rectToRebuilds)
        {
            if (item != null)
            {
                try
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(item);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("Error updating layout: " + ex.Message);
                }
            }
            else
            {
                //Debug.LogError("RectTransform is null.");
            }
        }
    }
}
}